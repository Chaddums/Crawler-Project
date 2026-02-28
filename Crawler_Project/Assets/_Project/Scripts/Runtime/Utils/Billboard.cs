using UnityEngine;

namespace DungeonCrawlerCarl
{
    public class Billboard : MonoBehaviour
    {
        [SerializeField] private bool _lockYAxis = true;

        private Transform _cameraTransform;

        private void Start()
        {
            _cameraTransform = Camera.main?.transform;
        }

        private void LateUpdate()
        {
            if (_cameraTransform == null) return;

            if (_lockYAxis)
            {
                Vector3 lookDir = _cameraTransform.forward;
                lookDir.y = 0;
                if (lookDir.sqrMagnitude > 0.001f)
                    transform.rotation = Quaternion.LookRotation(lookDir);
            }
            else
            {
                transform.rotation = _cameraTransform.rotation;
            }
        }
    }
}
