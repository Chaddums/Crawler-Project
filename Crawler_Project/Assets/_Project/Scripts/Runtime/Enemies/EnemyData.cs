using System.Collections.Generic;
using UnityEngine;

namespace DungeonCrawlerCarl
{
    [CreateAssetMenu(fileName = "NewEnemyData", menuName = "DCC/Enemy Data")]
    public class EnemyData : ScriptableObject
    {
        [Header("Identity")]
        public string EnemyName;
        [TextArea(2, 4)]
        public string Description;
        public int Level = 1;
        public EnemyTier Tier = EnemyTier.Normal;

        [Header("Visuals")]
        public Sprite Portrait;
        public Sprite[] DirectionalSprites;
        public RuntimeAnimatorController AnimatorController;

        [Header("Stats")]
        public StatBlock BaseStats = new();

        [Header("Combat")]
        public List<AbilityData> Abilities = new();

        [Header("Loot")]
        public LootTableData LootTable;
        public int ExperienceReward = 25;

        [Header("Audio")]
        public AudioClip[] AggroSounds;
        public AudioClip[] DeathSounds;

        [Header("Commentary")]
        [Tooltip("Lines the AI announcer might say when this enemy is killed.")]
        public string[] KillCommentary;
    }
}
