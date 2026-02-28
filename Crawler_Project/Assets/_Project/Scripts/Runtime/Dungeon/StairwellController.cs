using UnityEngine;

namespace DungeonCrawlerCarl
{
    public class StairwellController : MonoBehaviour
    {
        [Header("Layout")]
        [SerializeField] private Transform _arrivalPoint;
        [SerializeField] private Transform _shopArea;
        [SerializeField] private Transform _descensionGate;

        [Header("Next Floor")]
        [SerializeField] private FloorData _nextFloorData;

        public Transform ArrivalPoint => _arrivalPoint;
        public Transform ShopArea => _shopArea;
        public Transform DescensionGate => _descensionGate;
        public FloorData NextFloorData => _nextFloorData;

        private void Start()
        {
            // Place the player at the arrival point
            var playerObj = GameObject.FindWithTag("Player");
            if (_arrivalPoint != null && playerObj != null)
            {
                playerObj.transform.SetPositionAndRotation(_arrivalPoint.position, _arrivalPoint.rotation);
            }

            // Play the AI floor announcement via the CommentaryManager
            PlayAIAnnouncement();
        }

        private void PlayAIAnnouncement()
        {
            if (_nextFloorData == null) return;

            string announcement = _nextFloorData.AIAnnouncement;
            if (string.IsNullOrEmpty(announcement)) return;

            // Fire the global announcement event so any listener (CommentaryManager, UI, etc.) can handle it
            GameEvents.OnAIAnnouncementReceived?.Invoke(announcement);

            // Also try to play specific floor commentary entries if any exist
            if (_nextFloorData.FloorCommentary != null && _nextFloorData.FloorCommentary.Count > 0)
            {
                // Pick the first announcement-priority entry, or the first entry available
                CommentaryEntry entry = null;

                foreach (var ce in _nextFloorData.FloorCommentary)
                {
                    if (ce.Priority == CommentaryPriority.Announcement)
                    {
                        entry = ce;
                        break;
                    }
                }

                if (entry == null)
                    entry = _nextFloorData.FloorCommentary[0];

                GameEvents.OnCommentaryTriggered?.Invoke(entry);
            }

            Debug.Log($"[StairwellController] AI Announcement: {announcement}");
        }

        /// <summary>
        /// Called when the player steps through the descension gate or interacts with it.
        /// Loads the next floor scene via SceneLoader.
        /// </summary>
        public void DescendToNextFloor()
        {
            if (_nextFloorData == null)
            {
                Debug.LogWarning("[StairwellController] No next floor data assigned.");
                return;
            }

            var sceneLoader = ServiceLocator.Get<SceneLoader>();
            if (sceneLoader == null)
            {
                Debug.LogError("[StairwellController] SceneLoader not found in ServiceLocator.");
                return;
            }

            Debug.Log($"[StairwellController] Descending to floor {_nextFloorData.FloorNumber}: {_nextFloorData.FloorName}");
            sceneLoader.LoadFloor(_nextFloorData.FloorNumber);
        }
    }
}
