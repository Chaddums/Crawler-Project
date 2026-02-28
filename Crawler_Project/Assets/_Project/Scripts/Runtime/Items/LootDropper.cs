using System.Collections.Generic;
using UnityEngine;

namespace DungeonCrawlerCarl
{
    public class LootDropper : MonoBehaviour
    {
        [Header("Loot Settings")]
        [SerializeField] private LootTableData lootTable;
        [SerializeField] private float dropRadius = 1.5f;

        /// <summary>
        /// Resolves the configured loot table and spawns ItemPickup objects around this transform.
        /// </summary>
        /// <param name="luckModifier">Luck multiplier affecting rare+ drop rates.</param>
        public void DropLoot(float luckModifier = 1f)
        {
            if (lootTable == null)
            {
                Debug.LogWarning($"[LootDropper] No loot table assigned on {gameObject.name}.");
                return;
            }

            SpawnDrops(lootTable, transform.position, luckModifier);
        }

        /// <summary>
        /// Overload used by EnemyController: resolves the provided table and spawns drops at the given position.
        /// </summary>
        public void DropLoot(LootTableData table, Vector3 position, float luckModifier = 1f)
        {
            if (table == null)
            {
                Debug.LogWarning($"[LootDropper] Provided loot table is null on {gameObject.name}.");
                return;
            }

            SpawnDrops(table, position, luckModifier);
        }

        private void SpawnDrops(LootTableData table, Vector3 origin, float luckModifier)
        {
            List<ItemInstance> items = LootTableResolver.Resolve(table, luckModifier);

            if (items.Count == 0) return;

            float angleStep = 360f / items.Count;

            for (int i = 0; i < items.Count; i++)
            {
                var item = items[i];

                if (item.Data == null || item.Data.WorldPrefab == null)
                {
                    Debug.LogWarning($"[LootDropper] Item {item.Data?.ItemName ?? "null"} has no WorldPrefab. Skipping spawn.");
                    continue;
                }

                // Calculate a position in a circle around the origin
                float angle = angleStep * i * Mathf.Deg2Rad;
                float randomOffset = Random.Range(0.3f, 1f) * dropRadius;
                Vector3 offset = new Vector3(
                    Mathf.Cos(angle) * randomOffset,
                    0f,
                    Mathf.Sin(angle) * randomOffset
                );

                Vector3 spawnPosition = origin + offset;

                GameObject pickupObject = Instantiate(item.Data.WorldPrefab, spawnPosition, Quaternion.identity);

                // Initialize the ItemPickup component with the rolled item instance
                var pickup = pickupObject.GetComponent<ItemPickup>();
                if (pickup != null)
                {
                    pickup.Initialize(item);
                }
                else
                {
                    Debug.LogWarning($"[LootDropper] WorldPrefab for {item.Data.ItemName} is missing an ItemPickup component.");
                }
            }

            Debug.Log($"[LootDropper] Dropped {items.Count} item(s) from {table.TableName}.");
        }
    }
}
