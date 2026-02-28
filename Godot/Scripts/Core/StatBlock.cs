using System.Collections.Generic;

namespace JunkbotArena
{
    public class StatModifier
    {
        public StatType StatType;
        public ModifierType ModType;
        public float Value;
        public object Source;

        public StatModifier() { }

        public StatModifier(StatType statType, ModifierType modType, float value, object source = null)
        {
            StatType = statType;
            ModType = modType;
            Value = value;
            Source = source;
        }
    }

    public struct StatRange
    {
        public StatType Stat;
        public ModifierType ModType;
        public float MinValue;
        public float MaxValue;
    }

    public class StatBlock
    {
        public struct StatEntry
        {
            public StatType Type;
            public float BaseValue;

            public StatEntry(StatType type, float baseValue)
            {
                Type = type;
                BaseValue = baseValue;
            }
        }

        public List<StatEntry> BaseStats = new();
        private readonly Dictionary<StatType, List<StatModifier>> _modifiers = new();

        public float GetStat(StatType type)
        {
            float baseVal = 0f;
            for (int i = 0; i < BaseStats.Count; i++)
            {
                if (BaseStats[i].Type == type) { baseVal = BaseStats[i].BaseValue; break; }
            }

            float flat = 0f;
            float percent = 0f;

            if (_modifiers.TryGetValue(type, out var mods))
            {
                foreach (var mod in mods)
                {
                    if (mod.ModType == ModifierType.Flat)
                        flat += mod.Value;
                    else
                        percent += mod.Value;
                }
            }

            return (baseVal + flat) * (1f + percent);
        }

        public float GetBaseStat(StatType type)
        {
            for (int i = 0; i < BaseStats.Count; i++)
            {
                if (BaseStats[i].Type == type) return BaseStats[i].BaseValue;
            }
            return 0f;
        }

        public void SetBaseStat(StatType type, float value)
        {
            for (int i = 0; i < BaseStats.Count; i++)
            {
                if (BaseStats[i].Type == type)
                {
                    BaseStats[i] = new StatEntry(type, value);
                    return;
                }
            }
            BaseStats.Add(new StatEntry(type, value));
        }

        public void AddModifier(StatModifier mod)
        {
            if (!_modifiers.ContainsKey(mod.StatType))
                _modifiers[mod.StatType] = new List<StatModifier>();
            _modifiers[mod.StatType].Add(mod);
        }

        public void RemoveModifier(StatModifier mod)
        {
            if (_modifiers.TryGetValue(mod.StatType, out var mods))
                mods.Remove(mod);
        }

        public void RemoveModifiersFromSource(object source)
        {
            foreach (var kvp in _modifiers)
            {
                kvp.Value.RemoveAll(m => m.Source == source);
            }
        }

        public void ClearModifiers()
        {
            _modifiers.Clear();
        }

        public void CopyBaseStatsFrom(StatBlock other)
        {
            BaseStats = new List<StatEntry>(other.BaseStats);
        }
    }
}
