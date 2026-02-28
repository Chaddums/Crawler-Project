using UnityEngine;

namespace DungeonCrawlerCarl
{
    public class IsometricCameraController : MonoBehaviour
    {
        [Header("Isometric Settings")]
        [SerializeField] private float _cameraAngleX = Constants.CAMERA_ANGLE_X;
        [SerializeField] private float _cameraAngleY = Constants.CAMERA_ANGLE_Y;
        [SerializeField] private float _cameraDistance = Constants.CAMERA_DISTANCE;

        [Header("Zoom")]
        [SerializeField] private float _zoomSpeed = 2f;
        [SerializeField] private float _minZoom = Constants.CAMERA_MIN_ZOOM;
        [SerializeField] private float _maxZoom = Constants.CAMERA_MAX_ZOOM;
        [SerializeField] private float _zoomSmoothSpeed = 10f;

        [Header("Follow")]
        [SerializeField] private float _followSmoothSpeed = 8f;

        private Transform _followTarget;
        private float _targetZoom;
        private Vector3 _offset;

        private void Start()
        {
            _targetZoom = _cameraDistance;
            CalculateOffset();

            // Try to find player if no target set
            if (_followTarget == null)
            {
                var player = ServiceLocator.Get<PlayerController>();
                if (player != null)
                    _followTarget = player.transform;
            }
        }

        private void LateUpdate()
        {
            HandleZoom();
            FollowTarget();
        }

        public void Initialize(Transform target)
        {
            _followTarget = target;
            CalculateOffset();

            // Snap to target immediately
            if (_followTarget != null)
                transform.position = _followTarget.position + _offset;

            transform.rotation = Quaternion.Euler(_cameraAngleX, _cameraAngleY, 0);
        }

        private void CalculateOffset()
        {
            Quaternion rotation = Quaternion.Euler(_cameraAngleX, _cameraAngleY, 0);
            _offset = rotation * new Vector3(0, 0, -_cameraDistance);
        }

        private void HandleZoom()
        {
            float scrollDelta = Input.mouseScrollDelta.y;
            if (Mathf.Abs(scrollDelta) > 0.01f)
            {
                _targetZoom -= scrollDelta * _zoomSpeed;
                _targetZoom = Mathf.Clamp(_targetZoom, _minZoom, _maxZoom);
            }

            _cameraDistance = Mathf.Lerp(_cameraDistance, _targetZoom, Time.deltaTime * _zoomSmoothSpeed);
            CalculateOffset();
        }

        private void FollowTarget()
        {
            if (_followTarget == null) return;

            Vector3 targetPosition = _followTarget.position + _offset;
            transform.position = Vector3.Lerp(transform.position, targetPosition, Time.deltaTime * _followSmoothSpeed);
            transform.rotation = Quaternion.Euler(_cameraAngleX, _cameraAngleY, 0);
        }

        public void SetFollowTarget(Transform target)
        {
            _followTarget = target;
        }

        public void SnapToTarget()
        {
            if (_followTarget == null) return;
            transform.position = _followTarget.position + _offset;
        }
    }
}
