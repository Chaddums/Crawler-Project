using System.Collections.Generic;
using Godot;

namespace JunkbotArena
{
    [GlobalClass]
    public partial class StatusEffectData : Resource
    {
        [Export] public string Id { get; set; } = "";
        [Export] public string EffectName { get; set; } = "";
        [Export] public string Description { get; set; } = "";
        [Export] public float Duration { get; set; } = 5f;
        [Export] public float TickInterval { get; set; } = 1f;
        [Export] public float TickDamage { get; set; } = 0f;
        [Export] public DamageType TickDamageType { get; set; } = DamageType.Physical;
        [Export] public bool IsDebuff { get; set; } = true;

        // Stat modifications while effect is active
        public List<StatModifier> StatModifications { get; set; } = new();

        public StatusEffectData() { }

        public void AddStatMod(StatType stat, ModifierType mod, float value)
        {
            StatModifications.Add(new StatModifier(stat, mod, value));
        }
    }
}
