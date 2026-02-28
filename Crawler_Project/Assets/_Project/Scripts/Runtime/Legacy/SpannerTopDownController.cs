// Legacy Spanner script — simple top-down 8-direction movement.
// The Crawler project uses PlayerController.cs as a full hub component instead.
using UnityEngine;

namespace Crawler.Legacy
{
    public class SpannerTopDownController : MonoBehaviour
    {
        public float moveSpeed;

        void Update()
        {
            Vector3 moveInput = new Vector3(0f, 0f, 0f);
            moveInput.x = Input.GetAxisRaw("Horizontal");
            moveInput.y = Input.GetAxisRaw("Vertical");

            moveInput.Normalize();
            transform.position += moveInput * moveSpeed * Time.deltaTime;
        }
    }
}
