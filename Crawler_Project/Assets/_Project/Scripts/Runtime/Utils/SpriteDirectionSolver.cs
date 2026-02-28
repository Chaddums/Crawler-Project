using UnityEngine;

namespace DungeonCrawlerCarl
{
    public class SpriteDirectionSolver : MonoBehaviour
    {
        [SerializeField] private SpriteRenderer _spriteRenderer;
        [SerializeField] private Sprite[] _directionalSprites;

        private Transform _cameraTransform;

        private void Start()
        {
            _cameraTransform = Camera.main?.transform;
        }

        public void UpdateDirection(Vector3 worldMoveDirection)
        {
            if (_cameraTransform == null || _spriteRenderer == null) return;
            if (worldMoveDirection.sqrMagnitude < 0.01f) return;
            if (_directionalSprites == null || _directionalSprites.Length == 0) return;

            Vector3 camForward = _cameraTransform.forward;
            camForward.y = 0;
            camForward.Normalize();
            Vector3 camRight = _cameraTransform.right;
            camRight.y = 0;
            camRight.Normalize();

            float dotForward = Vector3.Dot(worldMoveDirection.normalized, camForward);
            float dotRight = Vector3.Dot(worldMoveDirection.normalized, camRight);

            float angle = Mathf.Atan2(dotRight, dotForward) * Mathf.Rad2Deg;
            int directionCount = _directionalSprites.Length;
            float segmentSize = 360f / directionCount;
            int index = Mathf.RoundToInt((angle + 180f) / segmentSize) % directionCount;

            if (index >= 0 && index < _directionalSprites.Length && _directionalSprites[index] != null)
                _spriteRenderer.sprite = _directionalSprites[index];
        }
    }
}
