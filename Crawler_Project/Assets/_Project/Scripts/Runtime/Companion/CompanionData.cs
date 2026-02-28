using System.Collections.Generic;
using UnityEngine;

namespace DungeonCrawlerCarl
{
    [CreateAssetMenu(fileName = "NewCompanionData", menuName = "DCC/Companion Data")]
    public class CompanionData : ScriptableObject
    {
        [Header("Identity")]
        public string CompanionName;
        public string Title;

        [Header("Visuals")]
        public Sprite Portrait;
        public Sprite[] DirectionalSprites;
        public RuntimeAnimatorController AnimatorController;

        [Header("Stats")]
        public StatBlock BaseStats = new();

        [Header("Abilities")]
        public List<AbilityData> StartingAbilities = new();

        [Header("Behavior")]
        public float BaseFollowDistance = 3f;
        public float BaseAggroRange = 8f;

        [Header("Audio")]
        public AudioClip[] IdleSounds;
        public AudioClip[] CombatSounds;

        [Header("Commentary")]
        [TextArea(1, 3)]
        public string[] ReactionLines;
    }
}
