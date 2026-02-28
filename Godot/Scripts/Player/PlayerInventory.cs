using System;
using System.Collections.Generic;
using Godot;

namespace DungeonCrawlerCarl
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

        public override void _Ready()
        {
            _stats = GetParent().GetNode<PlayerStats>("PlayerStats");
        }

        public bool TryAddItem(ItemInstance item)
        {
            if (_items.Count >= Constants.DEFAULT_INVENTORY_SIZE)
                return false;

            _items.Add(item);
            OnItemAdded?.Invoke(item);
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
