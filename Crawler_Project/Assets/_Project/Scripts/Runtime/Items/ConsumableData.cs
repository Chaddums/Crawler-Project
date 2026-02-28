using UnityEngine;

namespace DungeonCrawlerCarl
{
    [CreateAssetMenu(fileName = "NewConsumable", menuName = "DCC/Items/Consumable")]
    public class ConsumableData : ItemData
    {
        [Header("Consumable Effects")]
        public float HealAmount;
        public float ManaRestoreAmount;

        [Header("Buff")]
        public StatusEffectData AppliedBuff;
        public float Duration;

        [Header("Audio")]
        public AudioClip UseSound;
    }
}
