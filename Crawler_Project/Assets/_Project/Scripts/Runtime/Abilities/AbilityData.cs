using System.Collections.Generic;
using UnityEngine;

namespace DungeonCrawlerCarl
{
    [CreateAssetMenu(fileName = "NewAbility", menuName = "DCC/Ability")]
    public class AbilityData : ScriptableObject
    {
        [Header("Identity")]
        public string AbilityId;
        public string AbilityName;
        [TextArea] public string Description;
        [TextArea] public string FlavorText;
        public Sprite Icon;

        [Header("Execution")]
        public AbilityType Type;
        public TargetingType Targeting;
        public float BaseDamage;
        public DamageType DamageType;
        public float Range = 5f;
        public float Cooldown = 1f;
        public float ManaCost;
        public float CastTime;
        public float Duration;

        [Header("AoE")]
        public float AoERadius;

        [Header("Visual")]
        public GameObject EffectPrefab;
        public AudioClip CastSound;
        public AudioClip ImpactSound;
        public RuntimeAnimatorController CastAnimation;

        [Header("Effects")]
        public List<StatusEffectData> AppliedStatusEffects;
        public float KnockbackForce;
        public float StunDuration;

        [Header("Scaling")]
        public StatType ScalingStat = StatType.Strength;
        public float ScalingRatio = 0.5f;
    }
}
