// Legacy Spanner script — 2D platformer movement with jump.
// The Crawler project uses PlayerMovement.cs with NavMesh + isometric conversion instead.
using UnityEngine;

namespace Crawler.Legacy
{
    public class SpannerPlayerMovement2D : MonoBehaviour
    {
        public float moveSpeed = 5f;
        public float jumpForce = 10f;
        private Rigidbody2D rb;
        private bool isGrounded;

        void Start()
        {
            rb = GetComponent<Rigidbody2D>();
        }

        void Update()
        {
            float moveX = Input.GetAxis("Horizontal");
            rb.linearVelocity = new Vector2(moveX * moveSpeed, rb.linearVelocity.y);

            if (Input.GetKeyDown(KeyCode.Space) && isGrounded)
            {
                rb.linearVelocity = new Vector2(rb.linearVelocity.x, jumpForce);
            }
        }

        void OnCollisionStay2D(Collision2D collision)
        {
            isGrounded = true;
        }

        void OnCollisionExit2D(Collision2D collision)
        {
            isGrounded = false;
        }
    }
}
