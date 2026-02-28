using UnityEngine;

namespace DungeonCrawlerCarl
{
    [RequireComponent(typeof(Collider))]
    public class HurtBox : MonoBehaviour
    {
        [SerializeField] private Team _team;

        public Team Team => _team;
        public IDamageable Damageable { get; private set; }

        private void Awake()
        {
            Damageable = GetComponentInParent<IDamageable>();

            var col = GetComponent<Collider>();
            col.isTrigger = true;

            if (Damageable == null)
                Debug.LogWarning($"[HurtBox] No IDamageable found in parent of {gameObject.name}");
        }

        public void SetTeam(Team team) => _team = team;
    }
}
