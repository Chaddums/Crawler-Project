using UnityEngine;

namespace DungeonCrawlerCarl
{
    public class CommentaryTrigger : MonoBehaviour
    {
        [SerializeField] private CommentaryEntry _entry;
        [SerializeField] private bool _triggerOnce = true;
        [SerializeField] private TriggerCondition _condition = TriggerCondition.PlayerEntersArea;

        private bool _hasTriggered;

        public enum TriggerCondition
        {
            PlayerEntersArea,
            EnemyKilled,
            LootOpened,
            TimeBased,
            ManualCall
        }

        private void OnTriggerEnter(Collider other)
        {
            if (_condition != TriggerCondition.PlayerEntersArea) return;
            if (!other.CompareTag(Constants.TAG_PLAYER)) return;
            if (_triggerOnce && _hasTriggered) return;

            Fire();
        }

        public void Fire()
        {
            if (_triggerOnce && _hasTriggered) return;
            _hasTriggered = true;

            GameEvents.OnCommentaryTriggered?.Invoke(_entry);
        }

        public void Reset()
        {
            _hasTriggered = false;
        }
    }
}
