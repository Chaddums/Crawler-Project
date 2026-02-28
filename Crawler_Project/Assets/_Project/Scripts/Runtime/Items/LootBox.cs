using System.Collections.Generic;
using UnityEngine;

namespace DungeonCrawlerCarl
{
    public class LootBox : MonoBehaviour, IInteractable
    {
        [Header("Loot Box")]
        [SerializeField] private LootBoxTier tier;
        [SerializeField] private LootTableData lootTable;

        [Header("Effects")]
        [SerializeField] private Animator animator;
        [SerializeField] private ParticleSystem openEffect;
        [SerializeField] private AudioClip openSound;

        private bool _opened;
        private static readonly int OpenTrigger = Animator.StringToHash("Open");

        public string InteractionPrompt => $"Open {tier} Loot Box";
        public bool CanInteract => !_opened;

        public void Interact(GameObject playerObj)
        {
            if (_opened) return;
            _opened = true;

            // Get player luck stat for loot resolution
            float luckModifier = 1f;
            var attacker = playerObj.GetComponent<IAttacker>();
            if (attacker != null && attacker.Stats != null)
            {
                float luck = attacker.Stats.GetStat(StatType.Luck);
                // Normalize luck into a modifier: 10 is baseline, each point above adds 5%
                luckModifier = 1f + (luck - 10f) * 0.05f;
                luckModifier = Mathf.Max(0.1f, luckModifier);
            }

            // Resolve loot table
            List<ItemInstance> items = LootTableResolver.Resolve(lootTable, luckModifier);

            // Play open animation
            if (animator != null)
            {
                animator.SetTrigger(OpenTrigger);
            }

            // Play particle effect
            if (openEffect != null)
            {
                openEffect.Play();
            }

            // Play open sound
            if (openSound != null)
            {
                AudioSource.PlayClipAtPoint(openSound, transform.position);
            }

            // Fire loot box opened event
            GameEvents.OnLootBoxOpened?.Invoke(new LootBoxOpenedData
            {
                Tier = tier,
                Items = new List<object>(items)
            });

            // Generate commentary from Borant the AI dungeon master
            string commentaryText = GenerateBorantCommentary(items);
            var commentary = new CommentaryEntry
            {
                Speaker = "Borant",
                Text = commentaryText,
                Priority = CommentaryPriority.Medium,
                Category = CommentaryCategory.LootReaction
            };
            GameEvents.OnCommentaryTriggered?.Invoke(commentary);

            // Add items to player inventory
            var receiver = playerObj.GetComponent<IItemReceiver>();
            foreach (var item in items)
            {
                if (receiver == null || !receiver.TryAddItem(item))
                {
                    Debug.LogWarning($"[LootBox] Inventory full. Could not add {item.Data.ItemName}.");
                }
            }

            Debug.Log($"[LootBox] {tier} loot box opened. {items.Count} item(s) dropped.");
        }

        private string GenerateBorantCommentary(List<ItemInstance> items)
        {
            if (items.Count == 0)
                return "An empty box! The dungeon giveth... and the dungeon laugheth.";

            // Check for highest rarity among drops
            ItemRarity highestRarity = ItemRarity.Common;
            foreach (var item in items)
            {
                if (item.Data.Rarity > highestRarity)
                    highestRarity = item.Data.Rarity;
            }

            return highestRarity switch
            {
                ItemRarity.Legendary => "Now THAT is what we call a jackpot, folks! The crowd goes absolutely wild!",
                ItemRarity.Absurd => "I... I don't even know what that IS, but the audience is LOSING THEIR MINDS!",
                ItemRarity.Epic => "Ooh, shiny! The crawler is moving up in the world!",
                ItemRarity.Rare => "Not bad, not bad. The audience gives a polite golf clap.",
                ItemRarity.Uncommon => "Slightly better than garbage. The audience is mildly entertained.",
                _ => "Common loot. The audience yawns collectively."
            };
        }
    }
}
