using System;
using System.Collections.Generic;
using Godot;

namespace JunkbotArena
{
    /// <summary>
    /// Player inventory: item list + equipment slots with stat application.
    /// </summary>
    public partial class PlayerInventory : Node
    {
        private PlayerStats _stats;
        private readonly List<ItemInstance> _items = new();
        private readonly Dictionary<EquipmentSlot, ItemInstance> _equipped = new();

        public IReadOnlyList<ItemInstance> Items => _items;
        public IReadOnlyDictionary<EquipmentSlot, ItemInstance> Equipped => _equipped;
        public int ItemCount => _items.Count;

        public event Action<ItemInstance> OnItemAdded;
        public event Action<ItemInstance> OnItemRemoved;
        public event Action<EquipmentSlot, ItemInstance> OnEquipmentChanged;

        /// <summary>When true, TryAddItem won't fire OnItemPickedUp (used during save/load).</summary>
        public bool SuppressPickupEvents { get; set; }

        public override void _Ready()
        {
            _stats = GetParent().GetNode<PlayerStats>("PlayerStats");
        }

        public bool TryAddItem(ItemInstance item)
        {
            _items.Add(item);
            OnItemAdded?.Invoke(item);
            if (!SuppressPickupEvents)
                GameEvents.OnItemPickedUp?.Invoke(item.BaseData);

            GD.Print($"[Inventory] Added: {item.GetDisplayName()} ({item.Rarity})");
            return true;
        }

        public bool RemoveItem(ItemInstance item)
        {
            if (_items.Remove(item))
            {
                OnItemRemoved?.Invoke(item);
                return true;
            }
            return false;
        }

        /// <summary>Remove all items and equipment silently (used before save restore).</summary>
        public void ClearAll()
        {
            _items.Clear();
            foreach (var slot in new List<EquipmentSlot>(_equipped.Keys))
            {
                var item = _equipped[slot];
                _stats.Stats.RemoveModifiersFromSource(item);
                _equipped.Remove(slot);
            }
        }

        public void SwapSlots(int indexA, int indexB)
        {
            if (indexA < 0 || indexA >= _items.Count) return;
            if (indexB < 0 || indexB >= _items.Count) return;
            if (indexA == indexB) return;

            (_items[indexA], _items[indexB]) = (_items[indexB], _items[indexA]);
        }

        public bool Equip(ItemInstance item, EquipmentSlot? targetSlot = null)
        {
            if (item.BaseData is not EquipmentData equipData) return false;

            var slot = targetSlot ?? equipData.Slot;

            // Ring auto-fallback: if Ring1 is occupied, try Ring2
            if (slot == EquipmentSlot.Ring1 && !targetSlot.HasValue &&
                _equipped.ContainsKey(EquipmentSlot.Ring1) &&
                !_equipped.ContainsKey(EquipmentSlot.Ring2))
            {
                slot = EquipmentSlot.Ring2;
            }

            // Unequip current item in that slot first
            if (_equipped.TryGetValue(slot, out var current))
                Unequip(slot);

            // Remove from inventory bag, put in slot
            _items.Remove(item);
            _equipped[slot] = item;

            // Apply stat modifiers
            foreach (var mod in item.GetAllModifiers())
                _stats.Stats.AddModifier(mod);

            OnEquipmentChanged?.Invoke(slot, item);
            GameEvents.OnItemEquipped?.Invoke(item.BaseData);

            GD.Print($"[Inventory] Equipped: {item.GetDisplayName()} in {slot}");
            return true;
        }

        public bool Unequip(EquipmentSlot slot)
        {
            if (!_equipped.TryGetValue(slot, out var item)) return false;

            // Remove stat modifiers
            _stats.Stats.RemoveModifiersFromSource(item);

            _equipped.Remove(slot);
            _items.Add(item);

            OnEquipmentChanged?.Invoke(slot, null);
            GameEvents.OnItemUnequipped?.Invoke(item.BaseData);

            GD.Print($"[Inventory] Unequipped: {item.GetDisplayName()} from {slot}");
            return true;
        }

        /// <summary>
        /// Quick-use the best health consumable in inventory.
        /// Prioritizes smaller potions first to avoid waste.
        /// </summary>
        public bool UseHealthQuick()
        {
            var player = GetParent<PlayerController>();
            if (player == null || !player.Health.IsAlive) return false;
            if (player.Health.CurrentHealth >= player.Health.MaxHealth) return false;

            ItemInstance best = null;
            float bestHeal = float.MaxValue;

            foreach (var item in _items)
            {
                if (item.BaseData is ConsumableData c && c.HealAmount > 0)
                {
                    // Pick smallest potion that still heals meaningfully
                    if (c.HealAmount < bestHeal)
                    {
                        bestHeal = c.HealAmount;
                        best = item;
                    }
                }
            }

            if (best == null) return false;
            UseConsumable(best);
            return true;
        }

        /// <summary>
        /// Quick-use the best mana consumable in inventory.
        /// Prioritizes smaller packs first to avoid waste.
        /// </summary>
        public bool UseManaQuick()
        {
            if (_stats == null) return false;
            if (_stats.CurrentMana >= _stats.MaxMana) return false;

            ItemInstance best = null;
            float bestMana = float.MaxValue;

            foreach (var item in _items)
            {
                if (item.BaseData is ConsumableData c && c.ManaRestoreAmount > 0)
                {
                    if (c.ManaRestoreAmount < bestMana)
                    {
                        bestMana = c.ManaRestoreAmount;
                        best = item;
                    }
                }
            }

            if (best == null) return false;
            UseConsumable(best);
            return true;
        }

        public void UseConsumable(ItemInstance item)
        {
            if (item.BaseData is not ConsumableData consumable) return;

            var player = GetParent<PlayerController>();
            if (player == null) return;

            if (consumable.HealAmount > 0)
                player.Health.Heal(consumable.HealAmount);

            if (consumable.ManaRestoreAmount > 0)
                _stats.RestoreMana(consumable.ManaRestoreAmount);

            if (!string.IsNullOrEmpty(consumable.BuffId))
                ApplyConsumableBuff(player, consumable);

            GameEvents.OnItemUsed?.Invoke(item);

            item.StackCount--;
            if (item.StackCount <= 0)
                RemoveItem(item);

            GD.Print($"[Inventory] Used consumable: {consumable.ItemName}");
        }

        private void ApplyConsumableBuff(PlayerController player, ConsumableData consumable)
        {
            var statusMgr = player.GetNodeOrNull<StatusEffectManager>("StatusEffectManager");
            if (statusMgr == null) return;

            var effectData = new StatusEffectData
            {
                Id = consumable.BuffId,
                EffectName = consumable.ItemName,
                Duration = consumable.BuffDuration,
                IsDebuff = false
            };

            // Map buff IDs to stat modifications
            switch (consumable.BuffId)
            {
                case "buff_fortitude":
                    effectData.AddStatMod(StatType.Armor, ModifierType.Flat, 5f);
                    break;
                case "buff_adrenaline":
                    effectData.AddStatMod(StatType.AttackSpeed, ModifierType.Percent, 0.20f);
                    break;
            }

            statusMgr.ApplyEffect(effectData);
        }
    }
}
