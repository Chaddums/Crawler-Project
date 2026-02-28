using System;
using System.Collections.Generic;
using UnityEngine;

namespace DungeonCrawlerCarl
{
    public class PlayerInventory : MonoBehaviour
    {
        [SerializeField] private int _maxSlots = Constants.DEFAULT_INVENTORY_SIZE;

        private List<ItemInstance> _items = new();
        private Dictionary<EquipmentSlot, ItemInstance> _equipped = new();

        private PlayerStats _playerStats;
        private HealthComponent _health;
        private StatusEffectManager _statusEffects;

        public IReadOnlyList<ItemInstance> Items => _items;
        public IReadOnlyDictionary<EquipmentSlot, ItemInstance> Equipped => _equipped;
        public int MaxSlots => _maxSlots;
        public int UsedSlots => _items.Count;
        public bool HasSpace => _items.Count < _maxSlots;

        public event Action OnInventoryChanged;
        public event Action<EquipmentSlot> OnEquipmentChanged;

        private void Awake()
        {
            _playerStats = GetComponent<PlayerStats>();
            _health = GetComponent<HealthComponent>();
            _statusEffects = GetComponent<StatusEffectManager>();
        }

        public bool TryAddItem(ItemInstance item)
        {
            if (item == null) return false;

            // Try stacking
            if (item.Data.MaxStack > 1)
            {
                foreach (var existing in _items)
                {
                    if (existing.Data == item.Data && existing.StackCount < item.Data.MaxStack)
                    {
                        int canAdd = item.Data.MaxStack - existing.StackCount;
                        int toAdd = Mathf.Min(canAdd, item.StackCount);
                        existing.StackCount += toAdd;
                        item.StackCount -= toAdd;

                        if (item.StackCount <= 0)
                        {
                            OnInventoryChanged?.Invoke();
                            GameEvents.OnItemPickedUp?.Invoke(item.Data as ScriptableObject);
                            return true;
                        }
                    }
                }
            }

            if (_items.Count >= _maxSlots) return false;

            _items.Add(item);
            OnInventoryChanged?.Invoke();
            GameEvents.OnItemPickedUp?.Invoke(item.Data as ScriptableObject);
            return true;
        }

        public bool RemoveItem(ItemInstance item)
        {
            if (_items.Remove(item))
            {
                OnInventoryChanged?.Invoke();
                return true;
            }
            return false;
        }

        public void EquipItem(ItemInstance item)
        {
            if (item.Data is not EquipmentData equipData) return;

            if (_equipped.TryGetValue(equipData.Slot, out var current))
                UnequipItem(equipData.Slot);

            _equipped[equipData.Slot] = item;
            _items.Remove(item);
            ApplyEquipmentStats(item, true);

            OnEquipmentChanged?.Invoke(equipData.Slot);
            OnInventoryChanged?.Invoke();
            GameEvents.OnItemEquipped?.Invoke(item.Data as ScriptableObject);
        }

        public void UnequipItem(EquipmentSlot slot)
        {
            if (!_equipped.TryGetValue(slot, out var item)) return;
            if (!HasSpace) return;

            ApplyEquipmentStats(item, false);
            _items.Add(item);
            _equipped.Remove(slot);

            OnEquipmentChanged?.Invoke(slot);
            OnInventoryChanged?.Invoke();
            GameEvents.OnItemUnequipped?.Invoke(item.Data as ScriptableObject);
        }

        public ItemInstance GetEquipped(EquipmentSlot slot)
        {
            _equipped.TryGetValue(slot, out var item);
            return item;
        }

        public void SwapItems(int indexA, int indexB)
        {
            if (indexA < 0 || indexA >= _items.Count) return;
            if (indexB < 0 || indexB >= _items.Count) return;
            if (indexA == indexB) return;

            (_items[indexA], _items[indexB]) = (_items[indexB], _items[indexA]);
            OnInventoryChanged?.Invoke();
        }

        public int IndexOf(ItemInstance item) => _items.IndexOf(item);

        public void DropItem(ItemInstance item)
        {
            if (item == null) return;
            if (!_items.Remove(item)) return;

            // Spawn in world if possible
            if (item.Data.WorldPrefab != null)
            {
                var playerPos = transform.position + transform.forward * 1.5f;
                var pickup = Instantiate(item.Data.WorldPrefab, playerPos, Quaternion.identity);
                var itemPickup = pickup.GetComponent<ItemPickup>();
                if (itemPickup != null)
                    itemPickup.Initialize(item);
            }

            OnInventoryChanged?.Invoke();
        }

        public bool UseConsumable(ItemInstance item)
        {
            if (item == null || item.Data is not ConsumableData consumable) return false;

            // Apply healing
            if (consumable.HealAmount > 0 && _health != null)
                _health.Heal(consumable.HealAmount);

            // Restore mana
            if (consumable.ManaRestoreAmount > 0 && _playerStats != null)
                _playerStats.RestoreMana(consumable.ManaRestoreAmount);

            // Apply buff
            if (consumable.AppliedBuff != null && _statusEffects != null)
                _statusEffects.ApplyEffect(consumable.AppliedBuff, gameObject);

            // Play use sound
            if (consumable.UseSound != null)
                AudioSource.PlayClipAtPoint(consumable.UseSound, transform.position);

            // Consume one from stack
            item.StackCount--;
            if (item.StackCount <= 0)
                _items.Remove(item);

            OnInventoryChanged?.Invoke();
            return true;
        }

        private void ApplyEquipmentStats(ItemInstance item, bool equip)
        {
            if (_playerStats == null) return;
            if (item.Data is not EquipmentData equipData) return;

            if (equip)
            {
                foreach (var mod in equipData.FixedStats)
                {
                    mod.Source = item;
                    _playerStats.Stats.AddModifier(mod);
                }
                foreach (var mod in item.RolledStats)
                {
                    mod.Source = item;
                    _playerStats.Stats.AddModifier(mod);
                }
            }
            else
            {
                _playerStats.Stats.RemoveModifiersFromSource(item);
            }
        }
    }
}
