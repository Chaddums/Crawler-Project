using System.Collections.Generic;
using UnityEngine;

namespace DungeonCrawlerCarl
{
    [CreateAssetMenu(fileName = "NewStatusEffect", menuName = "DCC/Status Effect")]
    public class StatusEffectData : ScriptableObject
    {
        public string EffectName;
        [TextArea] public string Description;
        public Sprite Icon;
        public float Duration;
        public float TickInterval;
        public float TickDamage;
        public DamageType DamageType;
        public List<StatModifier> StatModifications;
        public bool Stackable;
        public int MaxStacks;
        public GameObject VisualEffectPrefab;
        public AudioClip ApplySound;
    }
}
