// Legacy Spanner script — brought in as reference for simple 2D chase behavior.
// The Crawler project uses EnemyAI.cs with a full state machine instead.
using UnityEngine;

namespace Crawler.Legacy
{
    public class SpannerEnemyChase : MonoBehaviour
    {
        public Transform player;
        public float chaseSpeed = 3f;

        void Update()
        {
            if (player != null)
            {
                Vector2 direction = (player.position - transform.position).normalized;
                transform.position = Vector2.MoveTowards(transform.position, player.position, chaseSpeed * Time.deltaTime);
            }
        }
    }
}
